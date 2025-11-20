<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>


    <xsl:template match="/">
        <services>
            <xsl:apply-templates select="/configuration/taxxor/components//*[(local-name()='web-application' or local-name()='service') and meta/details/repositories/repro]"/>
        </services> 
    </xsl:template>
    
    <xsl:template match="web-application|service">
        <service>
            <id><xsl:value-of select="@id"/></id>
            <xsl:apply-templates select="meta/details/repositories/repro"/>
        </service>
    </xsl:template>
    
    <xsl:template match="repro">
        <repro>
            <xsl:for-each select="@*">
                <xsl:element name="{local-name()}">
                    <xsl:value-of select="."/>
                </xsl:element>
            </xsl:for-each>
            <xsl:if test="not(@name)">
                <name><xsl:value-of select="@id"/></name>
            </xsl:if>
        </repro>
    </xsl:template>
</xsl:stylesheet>
