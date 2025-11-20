<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

    <xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>

    <xsl:template match="/">
        <xsl:apply-templates select="mappingcluster"/>
    </xsl:template>

    <xsl:template match="mappingcluster">
        <mappingCluster>
            <xsl:if test="string-length(projectid) &gt; 0">
                <xsl:attribute name="projectId">
                    <xsl:value-of select="projectid"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="string-length(internal/mappingfactid) &gt; 0">
                <xsl:attribute name="requestId">
                    <xsl:value-of select="internal/mappingfactid"/>
                </xsl:attribute>
            </xsl:if>

            <!-- Render internal entry -->
            <xsl:apply-templates select="internal"/>

            <!-- Render xbrl entries -->
            <xsl:apply-templates select="xbrl"/>

            <!-- Render fact -->
            <xsl:apply-templates select="fact"/>

        </mappingCluster>
    </xsl:template>

    <xsl:template match="fact">
        <fact>
            <xsl:if test="string-length(value) &gt; 0">
                <xsl:attribute name="value">
                    <xsl:value-of select="value"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="string-length(unit) &gt; 0">
                <xsl:attribute name="unit">
                    <xsl:value-of select="unit"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="string-length(periodtype) &gt; 0">
                <xsl:attribute name="period">
                    <xsl:value-of select="periodtype"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="string-length(basetype) &gt; 0">
                <xsl:attribute name="basetype">
                    <xsl:value-of select="basetype"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="string-length(displayvalue) &gt; 0">
                <xsl:attribute name="display">
                    <xsl:value-of select="displayvalue"/>
                </xsl:attribute>
            </xsl:if>
        </fact>
    </xsl:template>

    <xsl:template match="internal | xbrl">
        <entry scheme="{scheme}" status="{status}">
            <xsl:if test="string-length(period) &gt; 0">
                <xsl:attribute name="period">
                    <xsl:value-of select="period"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="string-length(flipsign) &gt; 0">
                <xsl:attribute name="flipsign">
                    <xsl:value-of select="flipsign"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="string-length(isflagged) &gt; 0">
                <xsl:attribute name="isFlagged">
                    <xsl:value-of select="isflagged"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="string-length(isabsolutedate) &gt; 0">
                <xsl:attribute name="isAbsolute">
                    <xsl:value-of select="isabsolutedate"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="sectionid">
                <xsl:attribute name="context">
                    <xsl:value-of select="sectionid"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="datatype">
                <xsl:attribute name="datatype">
                    <xsl:value-of select="datatype"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="displayas">
                <xsl:attribute name="displayOption">
                    <xsl:value-of select="displayas"/>
                </xsl:attribute>
            </xsl:if>
    

            <xsl:if test="string-length(normalize-space(mappingfactid)) &gt; 0 or string-length(normalize-space(query)) &gt; 0">
                <mapping>
                    <xsl:value-of select="mappingfactid | query"/>
                </mapping>
            </xsl:if>


            <xsl:if test="string-length(normalize-space(comment)) &gt; 0">
                <comment>
                    <xsl:value-of select="comment"/>
                </comment>
            </xsl:if>
        </entry>
    </xsl:template>

</xsl:stylesheet>
