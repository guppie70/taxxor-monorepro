<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    version="1.0">
    
    
    <xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>
    
    
    <xsl:template match="/">
        <div id="delete-report">
            <div class="top-bar">
                <button class="btn btn-xs btn-primary pull-right" onclick="deleteFilingSectionDataAll()">Delete all</button>
            </div>
            <xsl:apply-templates select="report/itemtodelete"/>
        </div>
    </xsl:template>
    
    
    <xsl:template match="itemtodelete">
        <xsl:variable name="has-references">
            <xsl:choose>
                <xsl:when test="references/reference">true</xsl:when>
                <xsl:otherwise>false</xsl:otherwise>
            </xsl:choose>
        </xsl:variable>
 
        <div class="alert alert-warning" role="alert" data-ref="{@data-ref}">
            <xsl:attribute name="class">
                <xsl:text>alert </xsl:text>
                <xsl:choose>
                    <xsl:when test="$has-references='true'">
                        <xsl:text>alert-warning</xsl:text>
                    </xsl:when>
                    <xsl:otherwise>alert-info</xsl:otherwise>
                </xsl:choose>
            </xsl:attribute>
            <button class="btn btn-xs btn-primary pull-right" onclick="deleteFilingSectionData('{@data-ref}')">Delete</button>
            <dl>
                <dt>Name</dt>
                <dd><xsl:value-of select="name"/></dd>
                <xsl:if test="$has-references='true'">
                    <dt>Referenced in <small>(will also be deleted)</small></dt>
                    <dd>
                        <ul>
                            <xsl:apply-templates select="references/reference"/>
                        </ul>
                    </dd>
                </xsl:if>
            </dl>

        </div>
        
    </xsl:template>
    
    
    <xsl:template match="reference">
        <li>
            <span>
                <xsl:value-of select="."/>
            </span>
            
            <xsl:choose>
                <xsl:when test="@from = 'hierarchy'">
                    <small>(Hierarchy: <xsl:value-of select="@name"/>)</small>
                    <xsl:if test="number(@children) > 0">
                        <br/>
                        <small class="children-message">Will also remove <xsl:value-of select="@children"/> items from the hierarchy!</small>
                    </xsl:if>
                </xsl:when>
                <xsl:otherwise>
                    <mark>NEW reference type (<xsl:value-of select="@from"/>)!</mark>
                </xsl:otherwise>
            </xsl:choose>
        </li>
    </xsl:template>
</xsl:stylesheet>